// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace ImagingCapture.Settings {
	///<summary>The imaging device the Capture settings screen is for: the fields of its saved ImagingDevice row that it needs.</summary>
	public sealed class CaptureSettingsDevice {
		public long ImagingDeviceNum { get; set; }
		public string Description { get; set; }="";
		public string TwainName { get; set; }="";
	}

	///<summary>One key of the Capture settings screen: its value in the file, the value being edited, and what is wrong with it.</summary>
	public sealed class CaptureSettingRow {
		internal CaptureSettingRow(CaptureSettingDef settingDef,long imagingDeviceNum) {
			Def=settingDef;
			SectionName=settingDef.GetSectionName(imagingDeviceNum);
			DisplayName=settingDef.GetDisplayName(imagingDeviceNum);
		}

		public CaptureSettingDef Def { get; }
		///<summary>"Device.12".</summary>
		public string SectionName { get; }
		///<summary>"[Device.12] Bits".</summary>
		public string DisplayName { get; }
		///<summary>The key is in the file (possibly empty).</summary>
		public bool IsPresent { get; internal set; }
		///<summary>The value in the file when it was loaded, trimmed; "" when missing.</summary>
		public string BaselineText { get; internal set; }="";
		///<summary>The value being edited, trimmed.</summary>
		public string Text { get; internal set; }="";
		///<summary>Why Text is not valid, or "".</summary>
		public string Problem { get; internal set; }="";
		///<summary>A gated key: its value when the screen last saved the device's settings on this computer, or null (never saved, or not gated).</summary>
		public string SavedText { get; internal set; }
		///<summary>A gated key whose value in the file differs from SavedText (as GatedSettingsHash compares them): changed outside Open Dental.</summary>
		public bool IsChangedOutside { get; internal set; }

		public bool IsChanged => Text!=BaselineText;
	}

	///<summary>What a Save did.</summary>
	public sealed class CaptureSettingsSaveResult {
		///<summary>capture.ini was written (or needed no write) and reads back as edited.</summary>
		public bool IsSaved { get; set; }
		///<summary>The Setup security-log entry with the new hash was written for the device. Without it, the hash check counts the new values as
		///changed outside Open Dental.</summary>
		public bool IsLogged { get; set; }
		///<summary>Why nothing was saved or logged, one or two sentences, or "".</summary>
		public string Error { get; set; }="";
		///<summary>capture.ini changed on disk since the screen loaded it, so nothing was saved: the screen must load it again first.</summary>
		public bool IsFileChangedSinceLoad { get; set; }
		///<summary>Problems that did not stop the save: the file had to go to the fallback path, a shared entry could not be logged, and the like.</summary>
		public List<string> ListWarnings { get; set; }=new List<string>();
		///<summary>The entry written for the device, or null.</summary>
		public CaptureSettingsLogEntry Entry { get; set; }
		///<summary>Entries written for other devices whose verified hash the shared [Session] and [Twain] keys changed.</summary>
		public List<CaptureSettingsLogEntry> ListEntriesShared { get; set; }=new List<CaptureSettingsLogEntry>();
	}

	///<summary>The state and logic of the Capture settings screen (design 9.3, 10.7), so the window stays thin and this can be tested with temporary
	///files and a fake store. It edits capture.ini of this computer through LocalCaptureConfig for one imaging device: [Session], [Twain], the
	///device's [Device.N], [Keys] and [Sounds] (CaptureSettingsCatalog; the camera sections stay with FormVideo's settings and Learn). Every value
	///is checked with the parsers the program reads it with (CaptureSettingsValidator); nothing is saved while one is invalid.
	///
	///Save (Setup permission) writes only the values that were edited, so other lines, comments and keys stay as they are, then reads the file
	///back, and writes a Setup security-log entry (PatNum 0, FKey=ImagingDeviceNum) with every changed key's old and new value, the gated values
	///and the new GatedSettingsHash for the device (CaptureSettingsLogEntry): that entry is what the hash check of the capture session, Test
	///capture and the 16-bit decision compares the file with (CaptureSettingsCheck). A save without edits is logged too: it accepts the values in
	///the file, which is how settings that were never saved, or changed outside Open Dental, become verified once a Setup user reviewed them.
	///
	///So that a value changed outside Open Dental is never accepted unseen, Save refuses when the file changed on disk since it was loaded (the
	///screen loads it again), and logs nothing when the file does not read back as edited. [Session] and [Twain] are in every device's hash: when
	///a save changes them, each other device whose settings were verified before gets an entry with its new hash too, so the change made here does
	///not count as a change outside Open Dental for it. All members are called on the UI thread.</summary>
	public sealed class CaptureSettingsModel {
		private readonly CaptureSettingsServices _services;
		private List<CaptureSettingRow> _listRows=new List<CaptureSettingRow>();

		public CaptureSettingsModel(CaptureSettingsDevice device,CaptureSettingsServices services) {
			Device=device??throw new ArgumentNullException(nameof(device));
			_services=services??throw new ArgumentNullException(nameof(services));
			if(device.ImagingDeviceNum<=0) {
				throw new ArgumentException("The device must be saved first (ImagingDeviceNum above 0).",nameof(device));
			}
			ComputerName=(_services.FuncGetComputerName?.Invoke()??"").Trim();
		}

		#region Properties
		public CaptureSettingsDevice Device { get; }
		public string ComputerName { get; }
		public IReadOnlyList<CaptureSettingRow> ListRows => _listRows;
		///<summary>The hash check of the file as loaded (design 9.3).</summary>
		public CaptureSettingsCheck SettingsCheck { get; private set; }
		///<summary>The file the values were read from (or the one a save will create), and why it could not be read, if so.</summary>
		public string FileText { get; private set; }="";
		///<summary>Changes whenever something the window shows changed.</summary>
		public int Version { get; private set; }

		public bool HasChanges => _listRows.Any(x => x.IsChanged);
		public int CountChanges => _listRows.Count(x => x.IsChanged);
		public bool HasProblems => _listRows.Any(x => x.Problem!="");
		#endregion Properties

		#region Methods - editing
		///<summary>Reads capture.ini and the hash check again; edits are discarded.</summary>
		public void Load() {
			LocalCaptureConfig config=LoadConfig();
			_listRows=new List<CaptureSettingRow>();
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings) {
				CaptureSettingRow row=new CaptureSettingRow(settingDef,Device.ImagingDeviceNum);
				row.IsPresent=config.ContainsKey(row.SectionName,settingDef.Key);
				row.BaselineText=GetValue(config,row);
				row.Text=row.BaselineText;
				_listRows.Add(row);
			}
			string path=config.PathLoaded??config.PathPrimary??"";
			FileText="capture.ini: "+(path=="" ? "(no path)" : path)+(config.PathLoaded==null ? " (not created yet: every value is its default)." : ".");
			if(!string.IsNullOrWhiteSpace(config.LastError)) {
				FileText+=" "+config.LastError.Trim();
			}
			SettingsCheck=CaptureSettingsCheck.Run(config,Device.ImagingDeviceNum,ComputerName,x => _services.Store.GetSetupLogEntries(x));
			foreach(CaptureSettingRow row in _listRows.Where(x => x.Def.IsGated)) {
				row.SavedText=SettingsCheck.LastSaved?.GetGatedValue(row.Def.Label);
				row.IsChangedOutside=row.SavedText!=null
					&& GatedSettingsHash.NormalizeValue(row.Def.Key,row.SavedText)!=GatedSettingsHash.NormalizeValue(row.Def.Key,row.BaselineText);
			}
			Validate();
			Version++;
		}

		///<summary>Sets the value being edited of ListRows[idx] (trimmed) and checks every value again. False for an index out of range.</summary>
		public bool SetText(int idx,string text) {
			if(idx<0 || idx>=_listRows.Count) {
				return false;
			}
			string textTrimmed=(text??"").Trim();
			if(_listRows[idx].Text==textTrimmed) {
				return true;
			}
			_listRows[idx].Text=textTrimmed;
			Validate();
			Version++;
			return true;
		}

		///<summary>Every value back to the one in the file as loaded.</summary>
		public void UndoChanges() {
			foreach(CaptureSettingRow row in _listRows) {
				row.Text=row.BaselineText;
			}
			Validate();
			Version++;
		}

		///<summary>The index of the row for section ("Device" for the device's section) and key, or -1.</summary>
		public int IndexOf(string section,string key) {
			return _listRows.FindIndex(x => string.Equals(x.Def.Section,section,StringComparison.OrdinalIgnoreCase)
				&& string.Equals(x.Def.Key,key,StringComparison.OrdinalIgnoreCase));
		}

		///<summary>The problems, one line each: "[Device.12] Bits=12 is not one of 8, 16."</summary>
		public List<string> GetProblemTexts() {
			return _listRows.Where(x => x.Problem!="").Select(x => x.Problem).ToList();
		}

		///<summary>The edits, as the security-log entry will list them, for the confirmation before Save.</summary>
		public List<CaptureSettingChange> GetPendingChanges() {
			return _listRows.Where(x => x.IsChanged).Select(x => ToChange(x,x.BaselineText,x.Text)).ToList();
		}

		///<summary>Unless the settings are Verified: the gated values in the file that differ from the ones last saved (changed outside Open Dental),
		///which a save accepts, each from its last saved value to the one in the file (design 9.3, 10.7). The confirmation before Save lists them,
		///and so does the security-log entry, so that a value changed outside is never accepted unseen, also by a save that edits another key.</summary>
		public List<CaptureSettingChange> GetChangesOutside() {
			if(SettingsCheck==null || SettingsCheck.IsVerified) {
				return new List<CaptureSettingChange>();
			}
			return _listRows.Where(x => x.Def.IsGated && x.IsChangedOutside).Select(x => {
				CaptureSettingChange change=ToChange(x,x.SavedText??"",x.BaselineText);
				change.IsChangedOutside=true;
				return change;
			}).ToList();
		}

		private void Validate() {
			Dictionary<CaptureSettingDef,string> dictTexts=_listRows.ToDictionary(x => x.Def,x => x.Text);
			Dictionary<CaptureSettingDef,string> dictProblems=CaptureSettingsValidator.Validate(dictTexts,Device.ImagingDeviceNum);
			foreach(CaptureSettingRow row in _listRows) {
				string problem;
				row.Problem=dictProblems.TryGetValue(row.Def,out problem) ? problem : "";
			}
		}
		#endregion Methods - editing

		#region Methods - save
		///<summary>Saves the edited values to capture.ini and logs the save (see the class summary). Never throws. Afterwards the screen shows the
		///file as saved (Load), unless nothing was written.</summary>
		public CaptureSettingsSaveResult Save() {
			CaptureSettingsSaveResult result=new CaptureSettingsSaveResult();
			try {
				SaveCore(result);
			}
			catch(Exception ex) {
				result.Error=(result.IsSaved ? "The capture settings were saved, but then: " : "The capture settings could not be saved: ")+ex.Message;
			}
			return result;
		}

		private void SaveCore(CaptureSettingsSaveResult result) {
			if(!_services.Store.IsAuthorized(EnumPermType.Setup)) {
				result.Error="Not authorized for Setup: capture settings were not saved.";
				return;
			}
			if(HasProblems) {
				result.Error="Fix these values first: "+string.Join(" ",GetProblemTexts());
				return;
			}
			LocalCaptureConfig configBefore=LoadConfig();
			List<string> listChangedOnDisk=_listRows.Where(x => GetValue(configBefore,x)!=x.BaselineText).Select(x => x.DisplayName).ToList();
			if(listChangedOnDisk.Count>0) {
				result.IsFileChangedSinceLoad=true;
				result.Error="capture.ini was changed since this screen read it ("+string.Join(", ",listChangedOnDisk)+"). Nothing was saved. Read the "
					+"file again and review the values before saving.";
				return;
			}
			List<CaptureSettingRow> listRowsChanged=_listRows.Where(x => x.IsChanged).ToList();
			//Taken before the save loads the check again: the values changed outside Open Dental that this save accepts.
			List<CaptureSettingChange> listChangesOutside=GetChangesOutside();
			bool isSharedChanged=listRowsChanged.Any(x => x.Def.IsGated && x.Def.Section!=CaptureSettingsCatalog.SectionDevice);
			//The other devices whose verified hash a change of the shared keys would change, with their hash before the save.
			Dictionary<long,string> dictHashesVerified=isSharedChanged ? GetOtherDevicesVerified(configBefore,result) : new Dictionary<long,string>();
			string hashBefore=GatedSettingsHash.Compute(configBefore,Device.ImagingDeviceNum);
			foreach(CaptureSettingRow row in listRowsChanged) {
				if(!configBefore.Set(row.SectionName,row.Def.Key,row.Text)) {
					result.Error="Nothing was saved: "+configBefore.LastError;
					return;
				}
			}
			if(!configBefore.Save()) {
				result.Error=configBefore.LastError;
				return;
			}
			if(!string.IsNullOrWhiteSpace(configBefore.LastError)) {
				result.ListWarnings.Add(configBefore.LastError.Trim());
			}
			LocalCaptureConfig configAfter=LoadConfig();
			List<string> listNotReadBack=_listRows.Where(x => GetValue(configAfter,x)!=x.Text).Select(x => x.DisplayName).ToList();
			if(listNotReadBack.Count>0) {
				//Saved where it is not read from (the fallback path while the ProgramData file exists), or changed by someone else at once: the
				//values in effect are not the ones reviewed here, so no hash is logged for them.
				result.Error="capture.ini does not read back as saved ("+string.Join(", ",listNotReadBack)+"), so no security-log entry was written "
					+"and the settings count as not verified."+(result.ListWarnings.Count==0 ? "" : " "+string.Join(" ",result.ListWarnings));
				Load();
				return;
			}
			result.IsSaved=true;
			DateTime now=_services.FuncNow?.Invoke()??DateTime.Now;
			List<CaptureSettingChange> listChanges=listChangesOutside.Concat(listRowsChanged.Select(x => ToChange(x,x.BaselineText,x.Text))).ToList();
			string note=listRowsChanged.Count==0 ? "Saved in Capture settings without changes: the values in the file were reviewed and accepted."
				: "Saved in Capture settings.";
			if(listChangesOutside.Count>0) {
				note+=" "+listChangesOutside.Count.ToString(CultureInfo.InvariantCulture)+" gated value(s) changed outside Open Dental since the last "
					+"save were accepted; each is listed from its last saved value to the one in the file.";
			}
			CaptureSettingsLogEntry entry=BuildEntry(Device.ImagingDeviceNum,Device.Description,configAfter,hashBefore,now,listChanges,note);
			try {
				_services.Store.MakeLogEntry(EnumPermType.Setup,0,entry.ToLogText(),Device.ImagingDeviceNum);
				result.IsLogged=true;
				result.Entry=entry;
			}
			catch(Exception ex) {
				result.Error="The capture settings were saved, but the security-log entry could not be written ("+ex.Message+"), so they count as not "
					+"verified. Save them again.";
			}
			if(result.IsLogged) {
				LogShared(dictHashesVerified,configAfter,now,listRowsChanged.Where(x => x.Def.IsGated && x.Def.Section!=CaptureSettingsCatalog.SectionDevice).ToList(),
					result);
			}
			Load();
		}

		///<summary>Each other imaging device whose settings were verified with configBefore: its ImagingDeviceNum and hash.</summary>
		private Dictionary<long,string> GetOtherDevicesVerified(LocalCaptureConfig configBefore,CaptureSettingsSaveResult result) {
			Dictionary<long,string> dictHashes=new Dictionary<long,string>();
			List<ImagingDevice> listImagingDevices;
			try {
				listImagingDevices=_services.Store.GetImagingDevices()??new List<ImagingDevice>();
			}
			catch(Exception ex) {
				result.ListWarnings.Add("The other imaging devices could not be read ("+ex.Message+"); their capture settings may count as changed outside "
					+"Open Dental until they are saved again.");
				return dictHashes;
			}
			foreach(long imagingDeviceNum in listImagingDevices.Where(x => x!=null).Select(x => x.ImagingDeviceNum)
				.Where(x => x>0 && x!=Device.ImagingDeviceNum).Distinct())
			{
				CaptureSettingsCheck check=CaptureSettingsCheck.Run(configBefore,imagingDeviceNum,ComputerName,x => _services.Store.GetSetupLogEntries(x));
				if(check.IsVerified) {
					dictHashes[imagingDeviceNum]=check.CurrentHash;
				}
			}
			return dictHashes;
		}

		private void LogShared(Dictionary<long,string> dictHashesVerified,LocalCaptureConfig configAfter,DateTime now,List<CaptureSettingRow> listRowsShared,
			CaptureSettingsSaveResult result)
		{
			List<ImagingDevice> listImagingDevices=null;
			foreach(KeyValuePair<long,string> pair in dictHashesVerified) {
				string hashAfter=GatedSettingsHash.Compute(configAfter,pair.Key);
				if(hashAfter==pair.Value) {
					continue;
				}
				if(listImagingDevices==null) {
					listImagingDevices=_services.Store.GetImagingDevices()??new List<ImagingDevice>();
				}
				string description=listImagingDevices.FirstOrDefault(x => x!=null && x.ImagingDeviceNum==pair.Key)?.Description??"";
				CaptureSettingsLogEntry entryShared=BuildEntry(pair.Key,description,configAfter,pair.Value,now,
					listRowsShared.Select(x => ToChange(x,x.BaselineText,x.Text)).ToList(),
					"Shared [Session] or [Twain] keys changed in Capture settings for device "+Device.ImagingDeviceNum.ToString(CultureInfo.InvariantCulture)
						+" ("+Device.Description+"); this device's own section is unchanged.");
				try {
					_services.Store.MakeLogEntry(EnumPermType.Setup,0,entryShared.ToLogText(),pair.Key);
					result.ListEntriesShared.Add(entryShared);
				}
				catch(Exception ex) {
					result.ListWarnings.Add("The security-log entry for device "+pair.Key.ToString(CultureInfo.InvariantCulture)+" could not be written ("
						+ex.Message+"); its capture settings count as changed outside Open Dental until they are saved again.");
				}
			}
		}

		private CaptureSettingsLogEntry BuildEntry(long imagingDeviceNum,string description,LocalCaptureConfig configAfter,string hashBefore,DateTime now,
			List<CaptureSettingChange> listChanges,string note)
		{
			CaptureSettingsLogEntry entry=new CaptureSettingsLogEntry {
				ImagingDeviceNum=imagingDeviceNum,
				DeviceDescription=description??"",
				ComputerName=ComputerName,
				FilePath=configAfter.PathLoaded??configAfter.PathSaved??"",
				GatedSettingsHash=GatedSettingsHash.Compute(configAfter,imagingDeviceNum),
				PreviousGatedSettingsHash=hashBefore??"",
				DateTimeSaved=now,
				Note=note,
				ListChanges=listChanges,
			};
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings.Where(x => x.IsGated)) {
				entry.DictGatedValues[settingDef.Label]=configAfter.GetString(settingDef.GetSectionName(imagingDeviceNum),settingDef.Key,"").Trim();
			}
			return entry;
		}

		private static CaptureSettingChange ToChange(CaptureSettingRow row,string oldValue,string newValue) {
			return new CaptureSettingChange { Section=row.SectionName,Key=row.Def.Key,OldValue=oldValue,NewValue=newValue,IsGated=row.Def.IsGated };
		}
		#endregion Methods - save

		private LocalCaptureConfig LoadConfig() {
			return _services.FuncLoadConfig?.Invoke()??LocalCaptureConfig.Load((string)null);
		}

		private static string GetValue(LocalCaptureConfig config,CaptureSettingRow row) {
			return (config.GetString(row.SectionName,row.Def.Key,"")??"").Trim();
		}
	}
}
